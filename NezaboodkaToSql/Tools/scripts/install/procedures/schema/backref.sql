/*---------------------------------------/
		Back References routines
--------------------------------------*/

DELIMITER //
DROP PROCEDURE IF EXISTS _before_alter_back_refs //
CREATE PROCEDURE _before_alter_back_refs()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`backref_upd_list`;
	CREATE TEMPORARY TABLE IF NOT EXISTS `nz_admin_db`.`backref_upd_list`(
		`field_owner_type_name` VARCHAR(128) NOT NULL
			CHECK(`owner_type_name` != ''),
		`field_name` VARCHAR(128) NOT NULL
			CHECK(`name` != ''),
		`new_back_ref_name` VARCHAR(128) DEFAULT NULL
			CHECK(`back_ref_name` != ''),

		CONSTRAINT `uc_pair`
			UNIQUE (`field_owner_type_name`, `field_name`)
	);
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _remove_back_refs //
CREATE PROCEDURE _remove_back_refs()
BEGIN
	DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN
		SIGNAL SQLSTATE 'HY000'
			SET MESSAGE_TEXT = "Some back references can't be removed";
	END;

	CALL _init_field_shadow(@db_name);
	CALL QEXEC(CONCAT(
		"UPDATE `", @db_name, "`.`field`
		SET `back_ref_name` = NULL
		WHERE `id` IN (
			SELECT `id`
			FROM `nz_admin_db`.`field_shadow`
			JOIN `nz_admin_db`.`backref_upd_list`
			ON `field_owner_type_name` = `owner_type_name`
				AND `field_name` = `name`
		)"
	));

	-- Remove back references pairs
	CALL QEXEC(CONCAT(
		"UPDATE `", @db_name, "`.`field` AS f1
		JOIN `", @db_name, "`.`field` AS f2
		ON f2.`id` = f1.`back_ref_id`
			AND f1.`back_ref_name` IS NULL
		SET f2.`back_ref_name` = NULL"
	));
	CALL QEXEC(CONCAT(
		"UPDATE `", @db_name, "`.`field`
		SET `back_ref_id` = NULL
        WHERE `back_ref_name` IS NULL;"
	));

	DELETE FROM `nz_admin_db`.`backref_upd_list`
	WHERE `new_back_ref_name` IS NULL;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _add_back_refs //
CREATE PROCEDURE _add_back_refs()
BEGIN
	DECLARE f_id INT UNSIGNED DEFAULT 0;
	DECLARE f_new_back_ref_name VARCHAR(128);

	DECLARE done BOOLEAN DEFAULT FALSE;
	DECLARE back_refs_cur CURSOR FOR
		SELECT `id`, `new_back_ref_name`
		FROM `nz_admin_db`.`field_shadow`
		JOIN `nz_admin_db`.`backref_upd_list`
		ON `field_owner_type_name` = `owner_type_name`
			AND `field_name` = `name`;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET done = TRUE;

	DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN
		SIGNAL SQLSTATE 'HY000'
			SET MESSAGE_TEXT = "Some back references can't be added";
	END;

	CALL _init_field_shadow(@db_name);

	OPEN back_refs_cur;
	FETCH back_refs_cur
	INTO f_id, f_new_back_ref_name;
	WHILE NOT done DO
		CALL QEXEC(CONCAT(
			"UPDATE `", @db_name, "`.`field`
			SET `back_ref_name` = '", f_new_back_ref_name, "'
			WHERE `id` = ", f_id, ";"
		));
		FETCH back_refs_cur
		INTO f_id, f_new_back_ref_name;
	END WHILE;
	CLOSE back_refs_cur;

-- Refresh back references
    CALL QEXEC(CONCAT(
		"UPDATE `", @db_name, "`.`field` AS f1
		JOIN `", @db_name, "`.`field` AS f2
		ON f2.`name` = f1.`back_ref_name`
			AND f2.`ref_type_id` = f1.`owner_type_id`
		SET f1.`back_ref_id` = f2.`id`;"
	));
	CALL QEXEC(CONCAT(
		"UPDATE `", @db_name, "`.`field` AS f1
		JOIN `", @db_name, "`.`field` AS f2
		ON f2.`id` = f1.`back_ref_id`
		SET f2.`back_ref_id` = f1.`id`,
			f2.`back_ref_name` = f1.`name`;"
	));

-- Check if all back references were added
	CALL QEXEC(CONCAT(
		"SELECT COUNT(`id`)
		INTO @back_refs_left
		FROM `", @db_name, "`.`field`
		WHERE NOT `back_ref_name` IS NULL
			AND `back_ref_id` IS NULL;"
	));
	IF @back_refs_left > 0 THEN
		SIGNAL SQLSTATE '45000';
	END IF;
END //
