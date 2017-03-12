CREATE TABLE `${db_name}`.`field` (
    `id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
    `owner_type_name` VARCHAR(128) NOT NULL
        CHECK(`owner_type_name` != ''),
    `owner_type_id` INT DEFAULT NULL,
    `name` VARCHAR(128) NOT NULL
        CHECK(`name` != ''),
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
    ) NOT NULL DEFAULT 'None',
    `back_ref_name` VARCHAR(128) DEFAULT NULL
        CHECK(`back_ref_name` != ''),
    `back_ref_id` INT DEFAULT NULL,
    
    FOREIGN KEY(`owner_type_id`)
        REFERENCES `type`(`id`)
        ON DELETE CASCADE,
    
    FOREIGN KEY(`ref_type_id`)
        REFERENCES `type`(`id`)
        ON DELETE RESTRICT,	-- to prevent deletion of referenced type
    
    FOREIGN KEY(`back_ref_id`)
        REFERENCES `field`(`id`)
        ON DELETE SET NULL,

    CONSTRAINT `uc_type_fields`
        UNIQUE (`owner_type_name`, `name`),

    CONSTRAINT `uc_table_columns`
        UNIQUE (`owner_type_name`, `col_name`)
) ENGINE=`INNODB`;
