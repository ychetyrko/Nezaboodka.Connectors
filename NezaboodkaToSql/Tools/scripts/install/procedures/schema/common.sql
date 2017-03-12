/*---------------------------------------/
			Common routines
--------------------------------------*/

DELIMITER //
DROP PROCEDURE IF EXISTS _temp_before_common //
CREATE PROCEDURE _temp_before_common()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`temp_type_fields`;
	CREATE TEMPORARY TABLE `nz_admin_db`.`temp_type_fields`(
		`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
		`col_name` VARCHAR(64) NOT NULL COLLATE `UTF8_GENERAL_CI`
			CHECK(`col_name` != ''),
		`type_name` VARCHAR(64) NOT NULL
			CHECK(`type_name` != ''),
		`is_nullable` BOOLEAN NOT NULL DEFAULT FALSE,
		`ref_type_id` INT DEFAULT NULL,
		`is_list` BOOLEAN NOT NULL DEFAULT FALSE,
		`compare_options` ENUM (
			'None',
			'IgnoreCase',
			'IgnoreNonSpace',
			'IgnoreSymbols',
			'IgnoreKanaType',
			'IgnoreWidth',
			'OrdinalIgnoreCase',
			'StringSort',
			'Ordinal'
		) NOT NULL DEFAULT 'None'
	) ENGINE=`MEMORY`;

-- Shadow tables

	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`type_shadow`;
	CREATE TEMPORARY TABLE `nz_admin_db`.`type_shadow`(
		`id` INT NOT NULL,
		`name` VARCHAR(128) NOT NULL UNIQUE,
		`table_name` VARCHAR(64) NOT NULL UNIQUE COLLATE `UTF8_GENERAL_CI`,
		`base_type_name` VARCHAR(128) NOT NULL DEFAULT ''
	) ENGINE=`MEMORY`;

	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`type_shadow_base`;
	CREATE TEMPORARY TABLE `nz_admin_db`.`type_shadow_base`
    LIKE `nz_admin_db`.`type_shadow`;

	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`type_closure_shadow`;
	CREATE TEMPORARY TABLE `nz_admin_db`.`type_closure_shadow`(
		`ancestor` INT NOT NULL,
		`descendant` INT NOT NULL
	) ENGINE=`MEMORY`;

	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`field_shadow`;
	CREATE TEMPORARY TABLE `nz_admin_db`.`field_shadow` (
		`id` INT NOT NULL,
		`owner_type_name` VARCHAR(128) NOT NULL,
		`owner_type_id` INT DEFAULT NULL,
		`name` VARCHAR(128) NOT NULL,
		`col_name` VARCHAR(64) NOT NULL COLLATE `UTF8_GENERAL_CI`,
		`type_name` VARCHAR(64) NOT NULL,
		`is_nullable` BOOLEAN NOT NULL DEFAULT FALSE,
		`ref_type_id` INT DEFAULT NULL,
		`is_list` BOOLEAN NOT NULL DEFAULT FALSE,
		`compare_options` ENUM (
			'None',
			'IgnoreCase',
			'IgnoreNonSpace',
			'IgnoreSymbols',
			'IgnoreKanaType',
			'IgnoreWidth',
			'OrdinalIgnoreCase',
			'StringSort',
			'Ordinal'
		) NOT NULL DEFAULT 'None',
		`back_ref_name` VARCHAR(128) DEFAULT NULL,
		`back_ref_id` INT DEFAULT NULL
	) ENGINE=`MEMORY`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _init_type_shadow //
CREATE PROCEDURE _init_type_shadow(
	source_db_name VARCHAR(64)
)
BEGIN
	DELETE FROM `nz_admin_db`.`type_shadow`;
	CALL QEXEC(CONCAT(
		"INSERT INTO `nz_admin_db`.`type_shadow`
		(`id`, `name`, `table_name`, `base_type_name`)
		SELECT `id`, `name`, `table_name`, `base_type_name`
		FROM `", source_db_name, "`.`type`;"
	));
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _init_type_shadow_base //
CREATE PROCEDURE _init_type_shadow_base(
	source_db_name VARCHAR(64)
)
BEGIN
	DELETE FROM `nz_admin_db`.`type_shadow_base`;
	CALL QEXEC(CONCAT(
		"INSERT INTO `nz_admin_db`.`type_shadow_base`
		(`id`, `name`, `table_name`, `base_type_name`)
		SELECT `id`, `name`, `table_name`, `base_type_name`
		FROM `", source_db_name, "`.`type`;"
	));
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _init_type_closure_shadow //
CREATE PROCEDURE _init_type_closure_shadow(
	source_db_name VARCHAR(64)
)
BEGIN
	DELETE FROM `nz_admin_db`.`type_closure_shadow`;
	CALL QEXEC(CONCAT(
		"INSERT INTO `nz_admin_db`.`type_closure_shadow`
		(`ancestor`, `descendant`)
		SELECT `ancestor`, `descendant`
		FROM `", source_db_name, "`.`type_closure`;"
	));
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _init_field_shadow //
CREATE PROCEDURE _init_field_shadow(
	source_db_name VARCHAR(64)
)
BEGIN
	DELETE FROM `nz_admin_db`.`field_shadow`;
	CALL QEXEC(CONCAT(
		"INSERT INTO `nz_admin_db`.`field_shadow`
		(`id`, `owner_type_name`, `owner_type_id`, `name`, `col_name`, `type_name`, `is_nullable`, `ref_type_id`, `is_list`, `compare_options`, `back_ref_name`, `back_ref_id`)
		SELECT `id`, `owner_type_name`, `owner_type_id`, `name`, `col_name`, `type_name`, `is_nullable`, `ref_type_id`, `is_list`, `compare_options`, `back_ref_name`, `back_ref_id`
		FROM `", source_db_name, "`.`field`;"
	));
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _temp_after_common //
CREATE PROCEDURE _temp_after_common()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`field_shadow`;
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`type_closure_shadow`;
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`type_shadow`;

	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`temp_type_fields`;   
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _get_type_new_fields_and_constraints //
CREATE PROCEDURE _get_type_new_fields_and_constraints(
	IN c_type_id INT,
	IN inheriting BOOLEAN,
	OUT fields_defs TEXT,
	OUT fields_constraints TEXT
)
BEGIN
	DECLARE cf_id INT DEFAULT NULL;	-- for constraints names
	DECLARE cf_col_name VARCHAR(64) DEFAULT NULL;
	DECLARE cf_type_name VARCHAR(128) DEFAULT NULL;
	DECLARE cf_ref_type_id INT DEFAULT NULL;
	DECLARE cf_is_nullable BOOLEAN DEFAULT FALSE;
	DECLARE cf_is_list BOOLEAN DEFAULT FALSE;
	DECLARE cf_compare_options VARCHAR(64);

	SET fields_defs = '';
	SET fields_constraints = '';

	DELETE FROM `nz_admin_db`.`temp_type_fields`;
	IF inheriting THEN	-- get all parents' fields
		CALL QEXEC(CONCAT(
			"INSERT INTO `nz_admin_db`.`temp_type_fields`
			(`id`, `col_name`, `type_name`, `is_nullable`, `ref_type_id`,
				`is_list`, `compare_options`)
			SELECT f.`id`, f.`col_name`, f.`type_name`, f.`is_nullable`, f.`ref_type_id`,
				f.`is_list`, f.`compare_options`
			FROM `", @db_name, "`.`field` AS f
			WHERE f.`owner_type_id` IN (
				SELECT clos.`ancestor`	-- get all super classes
				FROM `", @db_name, "`.`type_closure` AS clos
				WHERE clos.`descendant` = ", c_type_id, "
			);"
		));
	ELSE	-- get only NEW fields
		CALL QEXEC(CONCAT(
			"INSERT INTO `nz_admin_db`.`temp_type_fields`
			(`id`, `col_name`, `type_name`, `is_nullable`, `ref_type_id`,
				`is_list`, `compare_options`)
			SELECT f.`id`, f.`col_name`, f.`type_name`, f.`is_nullable`, f.`ref_type_id`,
				f.`is_list`, f.`compare_options`
			FROM `nz_admin_db`.`new_field` AS newf	-- only new fields
			LEFT JOIN `", @db_name, "`.`field` AS f
			ON f.`id` = newf.`id`
			WHERE f.`owner_type_id` IN (
				SELECT clos.`ancestor`	-- get all super classes
				FROM `", @db_name, "`.`type_closure` AS clos
				WHERE clos.`descendant` = ", c_type_id, "
			);"
		));
	END IF;

	BEGIN	
		DECLARE fields_done BOOLEAN DEFAULT FALSE;
		DECLARE fields_cur CURSOR FOR
			SELECT `id`, `col_name`, `type_name`, `is_nullable`, `ref_type_id`,
				`is_list`, `compare_options`
			FROM `nz_admin_db`.`temp_type_fields`;
		DECLARE CONTINUE HANDLER FOR NOT FOUND
			SET fields_done = TRUE;

		OPEN fields_cur;

		FETCH fields_cur
		INTO cf_id, cf_col_name, cf_type_name, cf_is_nullable, cf_ref_type_id,
			cf_is_list, cf_compare_options;
		WHILE NOT fields_done DO
			CALL _update_type_fields_def_constr(
				fields_defs, fields_constraints, inheriting,
				c_type_id, cf_id, cf_col_name, cf_type_name, cf_is_nullable, cf_ref_type_id,
				cf_is_list, cf_compare_options
			);
			FETCH fields_cur
			INTO cf_id, cf_col_name, cf_type_name, cf_is_nullable, cf_ref_type_id,
				cf_is_list, cf_compare_options;
		END WHILE;
	END;

	IF (LEFT(fields_defs, 1) = ',') THEN
		SET fields_defs = SUBSTRING(fields_defs, 2);
	END IF;

	IF (LEFT(fields_constraints, 1) = ',') THEN
		SET fields_constraints = SUBSTRING(fields_constraints, 2);
	END IF;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _update_type_fields_def_constr //
CREATE PROCEDURE _update_type_fields_def_constr(
	INOUT f_defs TEXT,
	INOUT f_constrs TEXT,
	IN inheriting BOOLEAN,
	IN c_type_id INT,
	IN cf_id INT,
	IN cf_col_name VARCHAR(64),
	IN cf_type_name VARCHAR(128),
	IN cf_is_nullable BOOLEAN,
	IN cf_ref_type_id INT,
	IN cf_is_list BOOLEAN,
	IN cf_compare_options VARCHAR(128)
)
BEGIN
	DECLARE constr_add_prefix TEXT DEFAULT 'CONSTRAINT FK_';
	DECLARE constr_add_prefix_full TEXT DEFAULT '';
	DECLARE field_type VARCHAR(128);

	IF NOT inheriting THEN
		SET constr_add_prefix = CONCAT('ADD ', constr_add_prefix);
	END IF;

	IF cf_ref_type_id IS NULL THEN
		IF NOT cf_is_list THEN
			SET field_type = cf_type_name;
			IF field_type LIKE 'VARCHAR(%' OR field_type = 'TEXT' THEN
				IF cf_compare_options = 'IgnoreCase' THEN
					SET field_type = CONCAT(field_type, ' COLLATE `utf8_general_ci`');
				END IF;
			ELSE	-- not string
				IF (NOT cf_is_nullable) THEN
					SET field_type = CONCAT(field_type, ' NOT NULL');
				END IF;
			END IF;
			
		ELSE	-- list
			SET field_type = 'BLOB';
		END IF;
	ELSE	-- reference
		-- FK Constraint name = FK_<type_id>_<field_id>
		SET constr_add_prefix_full = CONCAT('
			', constr_add_prefix, c_type_id, '_', cf_id);
		
		IF NOT cf_is_list THEN
			SET field_type = 'BIGINT(0)';
			SET f_constrs = CONCAT(f_constrs, ',
				', constr_add_prefix_full,'
				FOREIGN KEY (`', cf_col_name,'`)
					REFERENCES `db_key`(`sys_id`)
					ON DELETE SET NULL
					ON UPDATE SET NULL');
		ELSE	-- list
			SET field_type = 'INT';
			SET f_constrs = CONCAT(f_constrs, ',
				', constr_add_prefix_full,'
				FOREIGN KEY (`', cf_col_name,'`)
					REFERENCES `list`(`id`)
					ON DELETE SET NULL');
		END IF;
	END IF;

	SET f_defs = CONCAT(f_defs, ', `', cf_col_name, '` ', field_type);
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _remove_deleted_fields_from_table //
CREATE PROCEDURE _remove_deleted_fields_from_table()
BEGIN
	CALL QEXEC(CONCAT(
		"UPDATE `", @db_name, "`.`field`
		SET `back_ref_name` = NULL
		WHERE `back_ref_id` IN (
			SELECT `id`
			FROM `nz_admin_db`.`removing_fields_list`
		);"
	));
	CALL QEXEC(CONCAT(
		"DELETE FROM `", @db_name, "`.`field`
		WHERE `id` IN (
			SELECT `id`
			FROM `nz_admin_db`.`removing_fields_list`
		);"
	));
END //
