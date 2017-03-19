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
DROP PROCEDURE IF EXISTS _update_back_refs //
CREATE PROCEDURE _update_back_refs()
BEGIN
	DECLARE f_id INT UNSIGNED DEFAULT 0;
	DECLARE f_ref_type_id INT UNSIGNED DEFAULT 0;
	DECLARE old_back_ref_id INT UNSIGNED DEFAULT 0;
	DECLARE old_back_ref_name VARCHAR(128);
	DECLARE new_back_ref_name VARCHAR(128);

	DECLARE done BOOLEAN DEFAULT FALSE;
	DECLARE back_refs_cur CURSOR FOR
		SELECT `id`, `ref_type_id`,
			`back_ref_id`, `back_ref_name`,
			`new_back_ref_name`
		FROM `nz_admin_db`.`field_shadow`
		JOIN `nz_admin_db`.`backref_upd_list`
		ON `field_owner_type_name` = `owner_type_name`
			AND `field_name` = `name`;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET done = TRUE;

	CALL _init_field_shadow(@db_name);

-- Debug
	SELECT * FROM field_shadow
	WHERE NOT back_ref_id IS NULL;

	OPEN back_refs_cur;
	FETCH back_refs_cur
	INTO f_id, f_ref_type_id,
		old_back_ref_id, old_back_ref_name,
		new_back_ref_name;
	WHILE NOT done DO
		IF new_back_ref_name IS NULL THEN
			CALL _remove_back_ref(f_id, f_ref_type_id, old_back_ref_id);
		ELSE
			IF old_back_ref_name IS NULL THEN
				CALL _add_back_ref(f_id, f_ref_type_id, new_back_ref_name);
			ELSE
				CALL _remove_back_ref(f_id, f_ref_type_id, old_back_ref_id);
				CALL _add_back_ref(f_id, f_ref_type_id, new_back_ref_name);
			END IF;
		END IF;
		FETCH back_refs_cur
		INTO f_id, f_ref_type_id,
			old_back_ref_id, old_back_ref_name,
			new_back_ref_name;
	END WHILE;
	CLOSE back_refs_cur;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _remove_back_ref //
CREATE PROCEDURE _remove_back_ref(
	IN f_id INT UNSIGNED,
	IN f_ref_type_id INT UNSIGNED,
	IN old_back_ref_id INT UNSIGNED
)
BEGIN

-- Debug
	SELECT f_id, f_ref_type_id, old_back_ref_id;

	SET @prep_str = CONCAT(
		"UPDATE `", @db_name, "`.`field`
		SET `back_ref_name` = NULL,
			`back_ref_id` = NULL
		WHERE `id` = ", f_id, ";"
	);

	-- check validity
	PREPARE p_delete_back_ref FROM @prep_str;
	DEALLOCATE PREPARE p_delete_back_ref;

	INSERT INTO `nz_admin_db`.`alter_query`
	(`query_text`)
	VALUE
	(@prep_str);
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _add_back_ref //
CREATE PROCEDURE _add_back_ref(
	IN f_id INT UNSIGNED,
	IN f_ref_type_id INT UNSIGNED,
	IN new_back_ref_name VARCHAR(128)
)
BEGIN
DECLARE back_ref_field_id INT UNSIGNED DEFAULT NULL;

-- Debug
	SELECT f_id, f_ref_type_id, new_back_ref_name;

	SELECT `id`
	INTO back_ref_field_id
	FROM `field_shadow`
	WHERE `owner_type_id` = f_ref_type_id
		AND `name` = new_back_ref_name;

-- Debug
	SELECT back_ref_field_id;

	IF NOT back_ref_field_id IS NULL THEN
		SET @prep_str = CONCAT(
			"UPDATE `", @db_name, "`.`field`
			SET `back_ref_name` = ", new_back_ref_name, ",
				`back_ref_id` = ", back_ref_field_id, "
			WHERE `id` = ", f_id, ";"
		);

		-- check validity
		PREPARE p_delete_back_ref FROM @prep_str;
		DEALLOCATE PREPARE p_delete_back_ref;

		INSERT INTO `nz_admin_db`.`alter_query`
		(`query_text`)
		VALUE
		(@prep_str);
	ELSE
		SIGNAL SQLSTATE 'HY000'
			SET MESSAGE_TEXT = "Some back references can't be added";
	END IF;
END //
